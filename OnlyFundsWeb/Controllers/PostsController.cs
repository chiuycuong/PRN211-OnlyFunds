﻿using BusinessObjects;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;

using DataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

using DataAccess.IRepository;
using DataAccess.Repository;
using OnlyFundsWeb.Helpers;
using System.Linq;

namespace OnlyFundsWeb.Controllers
{
    public class PostsController : Controller
    {
        IWebHostEnvironment env;
        private IPostRepository postRepository = new PostRepository();
        private IUserRepository userRepository = new UserRepository();
        private ICategoryRepository categoryRepository = new CategoryRepository();
        private IPostCategoryMapRepository postCategoryMapRepository = new PostCategoryMapRepository();
        private ICommentRepository cmtRepository = new CommentRepository();
        public PostsController(IWebHostEnvironment env) => this.env = env;

        // GET: PostsController
        public ActionResult PostList()
        {
            return View();
        }

        public ActionResult GetPostByUser(string username, int? page)
        {
            try
            {
                if (username == null)
                {
                    return NotFound();
                }

                User user = userRepository.GetUserByName(username);
                if (user == null)
                {
                    return NotFound();
                }
                if (page == null)
                    page = 1;
                IEnumerable<Post> postList = postRepository.GetPostByUser(user, page.Value);
                int pageSize = 3;
                int count = postRepository.CountPostByUser(user);
                int end = count / pageSize;
                if (count % 3 != 0)
                {
                    end = end + 1;
                }
                ViewBag.end = end;
                ViewBag.user = user.Username;
                return View("PostList", postList);
            }
            catch (Exception ex)
            {
                ViewBag.error = ex.Message;
                return View("Error");
            }
        }

        // GET: PostsController/Details/5
        public ActionResult Details(int? id)
        {
            try
            {
                if(id == null)
                {
                    return NotFound();
                }
                Post post = postRepository.GetPostById(id.Value);
                if (post == null)
                    return NotFound();
                IEnumerable<Comment> cmt = cmtRepository.GetCommentsByPost(post.PostId);
                ViewBag.Comments = cmt;
                return View(post);
            }
            catch
            {
                return RedirectToAction(nameof(PostList));
            }
        }

        // GET: PostsController/Create
        public ActionResult Create()
        {
            if (HttpContext.Session.GetString("user") == null)
            {
                return RedirectToAction("Index", "User");
            }
            return View();
        }

        // POST: PostsController/Create
        [HttpPost]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormFile file, Post post, int[] category)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(post.PostTitle))
                    throw new Exception("Title is required");
                if (string.IsNullOrWhiteSpace(post.PostDescription))
                    throw new Exception("Description is required");
                string fileName = Utilities.UploadFile(file, env, "postfiles");
                post.PostId = postRepository.GetMaxPostId() + 1;
                post.UploaderUsername = HttpContext.Session.GetString("user");
                post.UploadDate = DateTime.Now;
                post.FileUrl = fileName;
                postRepository.InsertPost(post);
                foreach(int catID in category)
                {
                    PostCategoryMap map = new PostCategoryMap
                    {
                        CategoryId = catID,
                        PostId = post.PostId
                    };
                    postCategoryMapRepository.AddPostMap(map);
                }
                return RedirectToAction(nameof(GetPostByUser), new { username = post.UploaderUsername });
            }
            catch (Exception ex)
            {
                ViewBag.error = ex.Message;
                return View();
            }
        }

        // GET: PostsController/Edit/5
        public ActionResult Edit(int? id)
        {
            string username = HttpContext.Session.GetString("user");
            if (username == null)
                return RedirectToAction("Index", "User");
            if (id == null)
                return NotFound();
            Post post = postRepository.GetPostById(id.Value);
            if (post == null)
                return NotFound();
            if (!username.Equals(post.UploaderUsername))
            {
                ViewBag.Error = "You are not allowed to edit posts of others";
                return View("Error");
            }
            return View(post);
        }

        // POST: PostsController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, Post post, IFormFile file, int[] cate)
        {
            try
            {
                Post oldPost = postRepository.GetPostById(id);
                if (oldPost == null)
                    throw new Exception("Post not found");
                post.UploaderUsername = oldPost.UploaderUsername;
                post.UploadDate = oldPost.UploadDate;
                if (file == null)
                    post.FileUrl = oldPost.FileUrl;
                else
                {
                    Utilities.DeleteFile(oldPost.FileUrl, env, "postfiles");
                    post.FileUrl = Utilities.UploadFile(file, env, "postfiles");
                }
                IEnumerable<Category> postCatList = categoryRepository.GetCategoriesByPost(post.PostId);
                foreach(var category in postCatList)
                {
                    postCategoryMapRepository.DeleteMap(post, category);
                }
                foreach (int catID in cate)
                {
                    PostCategoryMap map = new PostCategoryMap
                    {
                        PostId = post.PostId,
                        CategoryId = catID
                    };
                    postCategoryMapRepository.AddPostMap(map);
                }
                postRepository.Update(post);
                return RedirectToAction(nameof(Details), new { id = post.PostId });
            }
            catch (Exception ex)
            {
                ViewBag.error = ex.Message;
                return View("Error");
            }
        }

        // POST: PostsController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            try
            {
                postRepository.DeletePost(id);
                return RedirectToAction(nameof(GetPostByUser), new { username = HttpContext.Session.GetString("user") });
            }
            catch (Exception ex)
            {
                ViewBag.error = ex.Message;
                return View();
            }
        }

        public IActionResult Cancel()
        {
            return View("Create");
        }
    }
}
