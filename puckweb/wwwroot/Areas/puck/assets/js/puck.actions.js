﻿//$(document).ready(function () {
//ini
var obj = {};
var ctop = $(".menutop");
var cleft = $(".leftarea");
var cright = $(".rightarea .content");
var cinterfaces = $("body>.main>.interfaces");
var cmsg = $(".top .message");
var cidCounter = 0;
var validationDummyCounter = 0;
var searchType = '';
var searchRoot = '';
var searchTerm = '';
var publishedContent = [];
var haveChildren = [];
var dbcontent = [];
var defaultLanguage = "en-gb";
var userRoles = [];
var languages;
var languageSortDictionary = [];
var rootLocalisations = [];
var startPath = "/";
var startPaths = [];
var startId;
var startIds;
var overlays = [];
var emptyGuid = '00000000-0000-0000-0000-000000000000';
var logHelper = new LogHelper();
var currentCacheKey = "";
var workflowItems = [];
var formDatas = [];
var workflowComments = [];
var contentLocks = [];
var latestWorkflowNotificationId = 0;

var newTemplateFolder = function (p) {
    getTemplateFolderCreateDialog(function (d) {
        var overlayEl = overlay(d, 500, 300, undefined, "New Template Folder");
        wireForm(overlayEl.find("form"), function (data) {
            getDrawTemplates(p);
            overlayClose();
        }, function (data) {
            overlayEl.find(".msg").show().html(data.message);
        });
    }, p);
}
var newTemplate = function (p) {
    getTemplateCreateDialog(function (d) {
        var overlayEl = overlay(d, 500, 300, undefined, "New Template");
        wireForm(overlayEl.find("form"), function (data) {
            getDrawTemplates(p, undefined, function () {
                cright.find(".node[data-id='" + data.name + "']").click();
            });
            overlayClose();
        }, function (data) {
            overlayEl.find(".msg").show().html(data.message);
        });
    }, p);
}
var showSearch = function (term, type, root) {
    getSearchView(term, function (d) {
        if (canChangeMainContent()) {
            location.hash = "#";
            cright.html(d);
            afterDom();
        }
    }, type, root);
}
var searchDialogClose = function () {
    if ($(".search_ops:visible").length > 0) {
        $(".search_ops:visible").fadeOut(function () { $(this).remove(); cleft.show(); });
        return true;
    }
    return false;
}
var searchDialog = function (root, f) {
    overlayClose();
    getSearchTypes(root, function (d) {
        if (searchDialogClose())
            return;

        if (!searchRoot.isEmpty() || !searchType.isEmpty()) {
            $(".search_options i").addClass("active");
        } else {
            $(".search_options i").removeClass("active");
        }

        var el = $(".interfaces .search_ops").clone();

        var close = $('<i class="overlay_close fas fa-minus-circle"></i>');
        close.click(function () { searchDialogClose(); });
        el.append(close);

        el.css({ left: cright.position().left - 30 + "px", width: "0px", top: "0px", height: $(window).height() - 90 + "px" });
        cright.append(el);
        el.animate({ width: "280px" }, 200, function () { if (f) f(); });
        $("input.search").animate({ width: 205, opacity: 1 }, 500);
        var types = el.find("select");
        types.html("<option value=''>None</option>");
        $(d).each(function () {
            types.append(
                "<option value='" + this.Type + "'>" + this.Name + "</option>"
            );
        });

        if (!searchTerm.isEmpty()) {
            el.find("input.search").val(searchTerm);
        }

        if (!searchType.isEmpty()) {
            el.find("select option[value='" + searchType + "']").attr("selected", "selected");
        }

        if (!searchRoot.isEmpty()) {
            var close = $('<i class="fas fa-minus-circle"></i>');
            var pathspan = $("<span/>").html(searchRoot);
            el.find(".pathvalue").html('').append(pathspan).append(close);
            close.click(function () {
                el.find(".pathvalue").html('');
                searchRoot = "";
            });
        }

        el.on("click", ".node span", function (e) {
            var node = $(this).parents(".node:first");
            var path = node.attr("data-path");
            var close = $('<i class="fas fa-minus-circle"></i>');
            var pathspan = $("<span/>").html(path);
            searchRoot = path;
            el.find(".pathvalue").html('').append(pathspan).append(close);
            close.click(function () {
                el.find(".pathvalue").html('');
                searchRoot = "";
            });
        });
        getDrawContent(startId, el.find(".node"));
        el.find("button.btn").click(function () {
            searchType = el.find("select").val();
            searchRoot = el.find(".pathvalue span").html() || '';
            var term = el.find(".search").val();
            searchTerm = term;
            showSearch(term, searchType, searchRoot);
        });
        el.find("input.search").keypress(function (e) {
            searchType = el.find("select").val();
            searchRoot = el.find(".pathvalue span").html() || '';
            var term = $(this).val();
            searchTerm = term;
            e = e || event;
            if ((e.keyCode || e.which || e.charCode || 0) === 13) {
                showSearch(term, searchType, searchRoot);
            };
        });
        cright.find(".search_ops input.search").focus();
    });
}
var showUserMarkup = function (username) {
    getUserMarkup(username, function (d) {
        var overlayEl = overlay(d, 580, undefined, undefined, "User");
        wireForm(overlayEl.find("form"), function (data) {
            showUsers();
            if (overlayEl.find("input[name=UserName]").val() == userName) {
                userRoles = overlayEl.find("select[name=Roles]").val();
                hideTopNav();
                getUserLanguage(function (d) { defaultLanguage = d; });
                //startPaths = !data.startPaths ? [] : data.startPaths.split(",");
                //startIds = !data.startNodeIds ? [] : data.startNodeIds.split(",");
            }
            overlayClose();
        }, function (data) {
            overlayEl.find(".msg").attr("tabindex", "0").show().html(data.message).focus();
            overlayEl.find("button.update").removeAttr("disabled");
        }, function () {
            overlayEl.find("button.update").attr("disabled", "disabled");
        });
    });
}

var showUserGroupMarkup = function (group) {
    getUserGroupMarkup(group,function (d) {
        var overlayEl = overlay(d, 580, undefined, undefined, "User Group");
        wireForm(overlayEl.find("form"), function (data) {
            if ($(".user_edit.settings").length>0) {
                var groupName = overlayEl.find("input[name='Name']").val();
                var groupRoles = overlayEl.find("select[name=Roles]").val();
                if (!overlayEl.find(":input:hidden[name='Id']").val()) {
                    var container = $(".user_edit.settings");
                    container.find(".user-groups-list").append(
                        '<li class="list-group-item" data-permissions="' + groupRoles.join(",") + '">' + groupName + '</li>'
                    );
                }
            }
            if ($(".user_groups.settings").length > 0) {
                var groupId = overlayEl.find("input[name='Id']").val();
                var groupName = overlayEl.find("input[name='Name']").val();
                var groupRoles = overlayEl.find("select[name=Roles]").val();
                var roleNames = [];
                for (var i = 0; i < groupRoles.length; i++) {
                    roleNames.push(overlayEl.find("select[name='Roles'] option[value='"+groupRoles[i]+"']").html());
                }
                var container = $(".user_groups.settings");

                if (groupId) {
                    var li = container.find("li[data-group-id='" + groupId + "']");
                    li.find(".title").html(groupName);
                    li.find(".roles").html(roleNames.join(","));
                } else {
                    container.find("ul").append(
                        '<li class= "list-group-item" data-group-id="' + groupId + '" >'
                        + '<h3 class="font-weight-light title">' + groupName + '</h3>'
                        + '<div class="roles">'
                        + roleNames.join(",")
                        + '</div>'
                        + '<button class="edit btn btn-link" data-group-name="' + groupName + '"><i class="fas fa-pencil-alt"></i>&nbsp;edit</button>'
                        + '<button class="delete btn btn-link" data-group-name="' + groupName + '"><i class="fas fa-trash"></i>&nbsp;delete</button>'
                        +'</li>'
                    );
                }
                
            }
            overlayEl.find(".overlay_close").click();
        }, function (data) {
            overlayEl.find(".msg").attr("tabindex", "0").show().html(data.message).focus();
            overlayEl.find("button.update").removeAttr("disabled");
        }, function () {
            overlayEl.find("button.update").attr("disabled", "disabled");
        });
    });
}

var showUserGroups = function () {
    getUserGroups(function (d) {
        var overlayEl = overlay(d, 580, undefined, undefined, "User Groups");
        overlayEl.on("click","button.create",function (e) {
            var el = $(this);
            showUserGroupMarkup("");
        });
        overlayEl.on("click","button.edit",function (e) {
            var el = $(this);
            showUserGroupMarkup(el.attr("data-group-name"));
        });
        overlayEl.on("click","button.delete",function (e) {
            var el = $(this);
            setDeleteUserGroup(el.attr("data-group-name"), function () {
                el.parents("li:first").remove();
            });
        });
    });
}

var puckusers;
var drawUser = function (user,container) {
    var el = cinterfaces.find(".usercard").clone();
    el.find(".card-title").html(user.FirstName?(user.FirstName + " " + user.Surname):"- Name -");
    el.find(".username").html(user.UserName);
    el.find(".email").html(user.Email);
    el.find(".roles").html(user.Roles.length + " roles");
    el.find(".startpaths").html(user.StartPaths.split(",").join("<br/>"));
    el.find(".language").html(user.UserVariant);
    el.find(".lastlogin").html(user.LastLoginDateString);
    el.find("[data-username]").attr({"data-username":user.UserName});
    container.append(el);
}
var showUsers = function () {
    cright.html("");
    showLoader(cright);
    getUsersJson(function (data) {
        //console.log("users",data);
        puckusers = data;
        if (!canChangeMainContent())
            return;
        var usersContainer = cinterfaces.find(".users-container").clone();
        cright.html(usersContainer);
        cright.find(".create").click(function (e) {
            e.preventDefault();
            showUserMarkup("");
        });
        cright.find(".groups").click(function (e) {
            e.preventDefault();
            showUserGroups();
        });
        var usersListContainer = cright.find(".row");
        usersContainer.find("input.usersearch").keyup(function (e) {
            var val = $(this).val();
            var matchedUsers = [];
            usersListContainer.html("");
            for (var i = 0; i < puckusers.length; i++) {
                var user = puckusers[i];
                if (user.FullName.toLowerCase().indexOf((val||"").toLowerCase()) > -1 || val.replace(/\s/g,"")=="") {
                    matchedUsers.push(user);
                    drawUser(user, usersListContainer);
                }
            }
            if (matchedUsers.length == 0) {
                usersListContainer.html("<b>no results.</b>");
            }
        });
        for (var i = 0; i < puckusers.length; i++) {
            puckusers[i].FullName = (puckusers[i].FirstName||"") + " " + (puckusers[i].Surname||"");
            drawUser(puckusers[i],usersListContainer);
        }
        cright.find(".edit").click(function (e) {
            e.preventDefault();
            var name = $(this).attr("data-username");
            showUserMarkup(name);
        });
        cright.find(".delete").click(function (e) {
            e.preventDefault();
            if (!confirm("sure?"))
                return;
            var el = $(this);
            var name = el.attr("data-username");
            setDeleteUser(name, function (d) {
                if (d.success) {
                    el.parents(".usercard").remove();
                } else {
                    msg(false, d.message);
                }
            });
        });
    });
}

var showWorkflowItems = function () {
    cright.html("");
    showLoader(cright);
    getWorkflowItems(function (html) {
        cright.html(html);
    });
}

//var showUsers = function () {
//    cright.html("");
//    showLoader(cright);
//    getUsers(function (data) {
//        if (!canChangeMainContent())
//            return;
//        cright.html(data);
//        cright.find(".create").click(function (e) {
//            e.preventDefault();
//            showUserMarkup("");
//        });
//        cright.find(".edit").click(function (e) {
//            e.preventDefault();
//            var name = $(this).attr("data-username");
//            showUserMarkup(name);
//        });
//        cright.find(".delete").click(function (e) {
//            e.preventDefault();
//            if (!confirm("sure?"))
//                return;
//            var el = $(this);
//            var name = el.attr("data-username");
//            setDeleteUser(name, function (d) {
//                if (d.success) {
//                    el.parents("tr:first").remove();
//                } else {
//                    msg(false, d.message);
//                }
//            });
//        });
//    });
//}
var revisionsFor = function (vcsv, id) {
    var variants = vcsv.split(",");
    if (variants.length == 1) {
        showRevisions(variants[0], id);
    } else {
        var markup = $(".interfaces .revision_for_dialog").clone();
        markup.find(".descendantscontainer").hide();
        for (var i = 0; i < variants.length; i++) {
            markup.find("select").append(
                "<option value='" + variants[i] + "'>" + variantNames[variants[i]] + "</option>"
            );
        }
        overlay(markup, 400, 150, undefined, "Revision Language");
        markup.find("button").click(function (e) {
            e.preventDefault();
            var variant = markup.find("select").val();
            showRevisions(variant, id);
            overlayClose();
        });
    }
}
var showTimedPublishDialog = function (id, variant) {
    getTimedPublishDialog(id, variant, function (html) {
        var overlayEl = overlay(html, 400, undefined, undefined, "Scheduled Publish");
        var form = overlayEl.find("form");
        wireForm(form, function (data) {
            msg(true, data.message);
            overlayClose();
        }, function (data) {
            msg(false, data.message);
            overlayClose();
        });
    });
}
var showAudit = function (id,variant, username,page, pageSize, container) {
    getAuditMarkup(id, variant, username, page, pageSize, function (html) {
        container.html(html);
        cright.find("li.page:not(.current)").click(function () {
            var el = $(this);
            var attPage = el.attr("data-page");
            showAudit(id, variant, username, attPage, pageSize, container);
        });
    });    
}
var timedPublish = function (vcsv, id) {
    var variants = vcsv.split(",");
    if (variants.length == 1) {
        showTimedPublishDialog(id,variants[0]);
    } else {
        var markup = $(".interfaces .revision_for_dialog").clone();
        markup.find(".descendantscontainer").hide();
        for (var i = 0; i < variants.length; i++) {
            markup.find("select").append(
                "<option value='" + variants[i] + "'>" + variantNames[variants[i]] + "</option>"
            );
        }
        overlay(markup, 400, 150, undefined, "Select Variant");
        markup.find("button").click(function (e) {
            e.preventDefault();
            var variant = markup.find("select").val();
            showTimedPublishDialog(id,variant);
            overlayClose();
        });
    }
}
var showRevisions = function (variant, id) {
    cright.html("");
    showLoader(cright);
    getRevisions(id, variant, function (data) {
        if (!canChangeMainContent())
            return;
        cright.html(data);
        cright.find(".compare").click(function (e) {
            e.preventDefault();
            var el = $(this);
            showCompare(el.attr("data-id"));
        });
        cright.find(".delete").click(function (e) {
            e.preventDefault();
            if (!confirm("sure?"))
                return;
            var el = $(this);
            setDeleteRevision(el.attr("data-id"), function (data) {
                if (data.success == true) {
                    el.parents("tr:first").remove();
                } else {
                    msg(false, data.message);
                }
            });
        });
        cright.find(".revert").click(function (e) {
            e.preventDefault();
            if (!confirm("sure?"))
                return;
            var el = $(this);
            setRevert(el.attr("data-id"), function (data) {
                if (data.success) {
                    displayMarkup(null, data.type, data.variant, undefined, data.id);
                } else {
                    msg(false, data.message);
                }
            });
        });
    });
}
var showCompare = function (id) {
    getCompareMarkup(id, function (data) {
        var overlayEl = overlay(data, undefined, undefined, undefined, "Compare");
        overlayEl.find("button.revert").click(function (e) {
            e.preventDefault();
            if (!confirm("sure?"))
                return;
            var el = $(this);
            setRevert(el.attr("data-id"), function (d) {
                if (d.success) {
                    overlayClose();
                    displayMarkup(null, d.type, d.variant, undefined, d.id);
                } else {
                    overlayClose();
                    msg(false, d.message);
                }
            });
        });
        var displays = overlayEl.find(".compare_revision>.fields");
        var first = displays.first();
        var second = displays.last();
        first.find(".fieldwrapper:not(.complex)").each(function (i) {
            var el = $(this);
            var propName = el.attr("data-fieldname");
            var el2 = second.find(".fieldwrapper[data-fieldname='" + propName + "']");
            var elval = el.find(".editor-field").addClass("compared");
            var el2val = el2.find(".editor-field").addClass("compared");
            if (el2.length == 0 || elval.html() != el2val.html()) {
                elval.css({ backgroundColor: "#ffeeee" });
                el2val.css({ backgroundColor: "#ffeeee" });
            } else {
                elval.css({ backgroundColor: "#eeffee" });
                el2val.css({ backgroundColor: "#eeffee" });
            }
        });
        second.find(".fieldwrapper:not(.complex) .editor-field:not(.compared)").css({ backgroundColor: "#ffeeee" });
    });
}
var showCacheInfo = function (path) {
    getCacheInfo(path, function (data) {
        if (data.success) {
            var markup = $(".main.grid .interfaces .cache_exclude_dialog").clone();
            var overlayEl=overlay(markup, 400, 150, undefined, "Cache");
            if (data.result) {
                markup.find("input").attr("checked", "checked");
            }
            overlayEl.find("button").click(function (e) {
                setCacheInfo(path, markup.find("input").is(":checked"), function (data) {
                    if (data.success) {
                        msg(true,"cache setting saved");
                        overlayClose();
                    } else {
                        msg(false, data.message);
                        overlayClose()
                    }
                });
            });
        } else {
            msg(false, data.message);
        }
    });
}
var showSettings = function (path) {
    cright.html("");
    showLoader(cright);
    getSettings(path, function (data) {
        cright.html(data);
        afterDom();
        //setup validation
        wireForm(cright.find('form'), function (data) {
            msg(true, "settings updated.");
            window.scrollTo(0, 0);
            getVariants(function (data) {
                languages = data;
                languageSortDictionary = [];
                for (var i = 0; i < data.length; i++) {
                    languageSortDictionary[data[i].Key] = i+1;
                }
            });
            cright.find(".submit button").removeAttr("disabled");
        }, function (data) {
            msg(false, data.message);
            cright.find(".submit button").removeAttr("disabled");
        }, function () {
            cright.find(".submit button").attr("disabled","disabled");
        });
        setChangeTracker();
    });
}
var editParameters = function (settingsType, modelType, propertyName, success) {
    getEditorParametersMarkup(function (data) {
        var overlayEl = overlay(data, 500, undefined, undefined, "Edit Parameters");
        var form = overlayEl.find("form");
        wireForm(form, function (data) {
            msg(true, "parameters updated");
            success();
            overlayClose();
        }, function (data) {
            msg(false, data.message);
        });
    }, settingsType, modelType, propertyName);
}
var showViews = function () {
    cright.html("");
    showLoader(cright);
    if (!canChangeMainContent())
        return false;
    getViews(function (data) {
        cright.html(data);
        afterDom();
    });
}
var showTasks = function () {
    cright.html("");
    showLoader(cright);
    if (!canChangeMainContent())
        return false;
    getTasks(function (data) {
        cright.html(data);
        afterDom();
        cright.find(".tasklist a").click(function (e) {
            e.preventDefault();
            var el = $(this);
            if (el.hasClass("create_task")) {
                createTask();
                return;
            }
            if (el.hasClass("delete")) {
                if (!confirm("sure?"))
                    return;
                $.post(el.attr("href"), function (d) {
                    if (d.success) {
                        msg(true, "task deleted");
                        el.parents("tr:first").remove();
                    } else {
                        msg(false, d.message);
                    }
                });
                return;
            }
            $.get(el.attr("href"), function (d) {
                var overlayEl = overlay(d, 500, undefined, undefined, "Edit Task");
                var showIntervalSeconds = function () {
                    if (overlayEl.find("[Name='Recurring']").is(":checked"))
                        overlayEl.find("[data-fieldname='IntervalSeconds']").show();
                    else
                        overlayEl.find("[data-fieldname='IntervalSeconds']").hide();
                }
                overlayEl.find("[Name='Recurring']").change(showIntervalSeconds);
                showIntervalSeconds();
                var form = overlayEl.find("form");
                wireForm(form, function (data) {
                    msg(true, "task updated");
                    overlayClose();
                    showTasks();
                }, function (data) {
                    msg(false, data.message);
                });
            });
        });
    });
}
var createTask = function () {
    getTaskCreateDialog(function (data) {
        var overlayEl = overlay(data, 400, 150, undefined, "Create Task");
        overlayEl.find("button").click(function (e) {
            e.preventDefault();
            var typeSelect = overlayEl.find("select[name=type]");
            var type = typeSelect.val();
            getTaskMarkup(function (data) {
                overlayClose();
                var overlayEl = overlay(data, 500, undefined, undefined, "Edit Task");
                var showIntervalSeconds = function () {
                    if (overlayEl.find("[Name='Recurring']").is(":checked"))
                        overlayEl.find("[data-fieldname='IntervalSeconds']").show();
                    else
                        overlayEl.find("[data-fieldname='IntervalSeconds']").hide();
                }
                overlayEl.find("[Name='Recurring']").change(showIntervalSeconds);
                showIntervalSeconds();
                var form = overlayEl.find("form");
                wireForm(form, function (data) {
                    msg(true, "task updated");
                    overlayClose();
                    showTasks();
                }, function (data) {
                    msg(false, data.message);
                });
            }, type);
        });
    });
}
jQuery.validator.setDefaults({
    ignore: ""
});
var checkEnter = function (e) {
    e = e || event;
    var txtArea = /textarea/i.test((e.target || e.srcElement).tagName);
    return txtArea || (e.keyCode || e.which || e.charCode || 0) !== 13;
}
var wireForm = function (form, success, fail,submit) {
    $.validator.unobtrusive.parse(form);
    form.keypress(checkEnter);
    //debugger;
    form.on("submit",function (e) {
        //debugger;
        if (tinyMCE != undefined) {
            tinyMCE.triggerSave();
        }
        if (form.valid()) {
            e.preventDefault();
            var values = form.serialize();
            var fd = new FormData(form.get(0));
            
            var doPost = function () {
                $.ajax({
                    url: form.attr("action"),
                    data: fd,
                    processData: false,
                    contentType: false,
                    type: 'POST',
                    success: function (data) {
                        if (data.success == true) {
                            success(data);
                        } else {
                            fail(data);
                        }
                    }
                });
            }
            if (submit) {
                if (!submit(fd))
                    doPost();
            } else {
                doPost();
            }
        } else {

            if (form.find(".editorContainer:hidden .input-validation-error").length > 0) {
                var editorContainers = form.find(".editorContainer:hidden .input-validation-error").parents(".editorContainer");
                editorContainers.each(function (i) {
                    var editorContainer = $(this);
                    editorContainer.show();
                    var expand = editorContainer.siblings(".titleContainer").find(".expand");
                    expand.removeClass("fa-chevron-right");
                    expand.addClass("fa-chevron-down");
                });
            }

            var err_el = form.find(".input-validation-error:first");
            form.find("[href='#" + err_el.parents(".tab-pane").attr("id") + "']").click();
            if (err_el.is(":visible")) {
                err_el.focus();
            } else {
                err_el.parents("[tabindex]:first").focus();
            }
            
        }
    });
}
var newContent = function (guid, type) {
    getCreateDialog(function (data) {
        var overlayEl = overlay(data, 400, 250, 100, "New Content");
        overlayEl.find("button").click(function () {
            var type = overlayEl.find("select[name=type]").val();
            var variant = overlayEl.find("select[name=variant]").val();
            overlayClose()
            displayMarkup(guid,type, variant);
        });
    }, type);
}
var getDrawTemplates = function (path, el, f) {
    if (el == undefined) {
        var nodeParent = dirOfPath(path);
        el = $("ul.templates .node[data-path='" + nodeParent + "']");
    }
    getTemplates(path, function (data) {
        drawTemplates(data, el);
        if (f != undefined)
            f();
    });
}
var deleteTemplate = function (name, path) {
    if (!confirm("delete template - " + name + " ?"))
        return;
    setDeleteTemplate(path, function (d) {
        if (d.success)
            $("ul.templates .node[data-path='" + path + "']").remove();
        else msg(false, d.message);
    });
}
var deleteTemplateFolder = function (name, path) {
    if (!confirm("delete template folder - " + name + " ?"))
        return;
    setDeleteTemplateFolder(path, function (d) {
        if (d.success)
            $("ul.templates .node[data-path='" + path + "']").remove();
        else msg(false, d.message);
    });
}
var drawTemplates = function (data, el, sortable) {
    var str = "";
    var toAppend = $("<ul/>");
    for (var i = 0; i < data.length; i++) {
        var node = data[i];
        var elnode = $("<li/>").addClass("node");
        var elinner = $("<div class='inner'/>");
        elnode.append(elinner);
        elinner.append($("<i class=\"puck_icon\"></i>").addClass(node.Type == "folder" ? "fas fa-folder" : ""))
        elinner.append($("<i class=\"fas fa-chevron-right expand\"></i>"))
        elinner.append($("<i class=\"fas fa-cog menu\"></i>"))
                .append("<span class='nodename'>" + node.Name + "&nbsp;" + "</span>");
        elnode.attr({
            "data-type": node.Type
            , "data-path": node.Path
            , "data-id": node.Path
            , "data-name": node.Name
            , "data-has_children": node.HasChildren
        });
        if (!node.HasChildren)
            elnode.find(".expand").css({ visibility: "hidden" });
        toAppend.append(elnode);
    }
    el.find("ul").remove();
    el.append(toAppend);
}

var getDrawContent = function (id, el, sortable, f, renderVariantLinks, _startPaths) {
    sortable = sortable && userRoles.contains("_sort");
    var validStartPaths = [];
    if (_startPaths != undefined) {
        for (var i = 0; i < _startPaths.length; i++) {
            var valid = false;
            for (var j = 0; j < startPaths.length; j++) {
                if ((_startPaths[i]+"/").indexOf(startPaths[j]+"/")==0)
                    valid = true;
            }
            if (valid || startPaths.length==0)
                validStartPaths.push(_startPaths[i]);
        }
        if (validStartPaths.length > 0)
            _startPaths = validStartPaths;
        else
            _startPaths = startPaths;
    }
    _startPaths = _startPaths || startPaths;
    renderVariantLinks = renderVariantLinks || false;
    if (el == undefined) {
        el = cleft.find(".node[data-id='" + id + "']");
    }
    getMinimumContentByParentId(id, function (data) {
        /*var plevel = path.split('/').length - 1;
        for (var k in publishedContent) {
            var level = k.split('/').length - 1;
            if (plevel == level) {
                publishedContent[k] = undefined;
            }
        }*/
        for (k in data.current) {
            publishedContent[k] = undefined;
        }
        if (id != emptyGuid && jQuery.isEmptyObject(data.current))
            setHasChildren(id, function () { });
        for (var k in data.published) {
            publishedContent[k] = data.published[k];
        }
        for (var i = 0; i < data.children.length; i++) {
            haveChildren[data.children[i]] = true;
        }
        draw(data.current, el, sortable, renderVariantLinks, _startPaths);
        el.find(".node").each(function () {
            var n = $(this);
            if (!haveChildren[n.attr("data-id")]) {
                n.find(".expand").css({ visibility: "hidden" });
                n.attr("data-has_children", "false");
            } else
                n.attr("data-has_children", "true");
        });
        if (f != undefined)
            f();
    });
}
var draw = function (data, el, sortable, renderVariantLinks,_startPaths) {
    renderVariantLinks = renderVariantLinks || false;
    var str = "";
    var toAppend = $("<ul/>");
    var textIndent = el.parents(".node").length * 13;
    for (var p in data) {//ids as keys
        dbcontent[p] = data[p];
        var variants = [];
        var hasUnpublished = false;
        for (var v in data[p]) {//variant as keys
            variants.push(v);
            if (data[p][v].Published == false) {
                hasUnpublished = true;
            }
        }
        var node;
        if (!!data[p][defaultLanguage])
            node = data[p][defaultLanguage];
        else
            node = data[p][variants[0]];
        var elnode = $("<li/>").addClass("node");
        var disabled = _startPaths.length>0;
        for (var i = 0; i < _startPaths.length; i++) {
            if ((node.Path+"/").indexOf(_startPaths[i]+"/") == 0)
                disabled = false;
        }
        if (disabled) {
            elnode.addClass("disabled");
            elnode.data("disabled", true);
        }
        var elinner = $("<div class=\"inner\" style=\"text-indent:" + textIndent + "px\"/>");
        elnode.append(elinner);
        if (hasUnpublished)
            elnode.addClass("unpublished");
        elinner.append($("<i class=\"fas fa-search icon_search\"></i>"));
        var customClasses = window.treeIconClasses? (window.treeIconClasses[node.Type] || "") : "";
        elinner.append($("<i class=\"puck_icon " + customClasses +"\"></i>"));
        elinner.append($("<img/>").attr({"class":"loader", "src": "/areas/puck/assets/img/tree-loader.gif" }).hide());
        elinner.append($("<i class=\"fas fa-chevron-right expand\"></i>"))
        if(!disabled)
            elinner.append($("<i class=\"fas fa-cog menu\"></i>"));
        elinner.append("<span class='nodename'>" + node.NodeName + ((!renderVariantLinks||disabled) ? "&nbsp;" : "") + "</span>");
        var elVariants = $("<div class=\"variants d-inline\"/>");
        elinner.append(elVariants);
        var hasPublished = false;
        var hasUnpublished = false;
        for (var i = 0; i < variants.length; i++) {
            var a = $('<a title="' + variantNames[variants[i]] +'" class="variantLink" href="#content?id=' + node.Id + '&variant=' + variants[i] + '"/>');
            var vel = $("<span title=\"" + variantNames[variants[i]] + "\" class=\"variant\"/>").attr("data-variant", variants[i]).html(variants[i] + ((!renderVariantLinks||disabled) ? "&nbsp;" : ""));
            if (publishedContent[node.Id] != undefined && publishedContent[node.Id][variants[i]] != undefined) {
                vel.addClass("published");
                hasPublished = true;
            } else {
                hasUnpublished = true;
            }
            if (renderVariantLinks && !disabled) {
                a.append(vel);
                elVariants.append(a);
            } else elVariants.append(vel);
        }
        if (variants.length > 1) {
            elnode.addClass("many-variants");
            var setupVariantsPopover = function (elVariants,elinner,hasPublished,hasUnpublished) {
                elVariants.hide().removeClass("d-inline");
                var trigger = $("<div class=\"variantsPopoverTrigger d-inline fas fa-globe\"></div>");
                if (hasPublished && hasUnpublished)
                    trigger.addClass("has-published-and-unpublished");
                else if (hasPublished)
                    trigger.addClass("has-published");
                else if (hasUnpublished)
                    trigger.addClass("has-unpublished");
                elinner.append(trigger);
                trigger.attr({ "title": "Variants", "data-content": elVariants.html(), "data-toggle": "popover" });
                if (!disabled) {
                    trigger.popover({
                        html: true,
                        placement: "bottom",
                        container: elinner
                    });
                    trigger.on('shown.bs.popover', function () {
                        elinner.find(".popover .variant").each(function () {
                            var el = $(this);
                            var variant = el.html().replace("&nbsp;", "");
                            el.attr("data-variant", variant);
                            el.html(variantNames[variant]);
                            if (elinner.find(".variants .variant.selected[data-variant='" + variant + "']").length > 0)
                                el.addClass("selected");
                        });
                        $(document).on("click.popover", function (e) {
                            trigger.popover('hide');
                            $(document).off("click.popover");
                        });
                        var scrollParent = el.parents(".scrollContainer:first").find(".simplebar-content-wrapper");
                        scrollParent.scrollLeft(10);//trigger re-positioning of bootstrap popover to fit within boundary
                        scrollParent.scrollLeft(0);
                        //scrollParent.parents().scrollLeft(0);
                    });
                }
            }
            setupVariantsPopover(elVariants, elinner, hasPublished, hasUnpublished);
        }
        elnode.attr({
            "data-type_chain": typeFromChain(node.TypeChain)
            , "data-type": node.Type
            , "data-id": node.Id
            , "data-parent_id": node.ParentId
            , "data-path": node.Path
            , "data-nodename": node.NodeName
            , "data-variants": variants.join(",")
            , "data-parent_path": dirOfPath(node.Path)
            , "data-children_path": node.Path + "/"
            , "data-published": node.Published
        });
        toAppend.append(elnode);
    }
    el.find("ul").remove();
    el.append(toAppend);
    if (sortable && !disabled) {
        toAppend.sortable({
            cursorAt: { top: 0, left: 0 },
            axis:"y",
            update: function (event, ui) {
                var parent = ui.item.parents("li[data-children_path]:first");
                var sortParentId = parent.attr("data-id");
                var items = [];
                parent.find("li.node").each(function () {
                    items.push($(this).attr("data-id"));
                });
                sortNodes(sortParentId, items, function () { });
            }
        });
    }
}

var showLoader = function (container) {
    var el = $("<div class='loaderContainer'/>").css({
        "display": "flex",
        "justify-content": "center",
        "align-items": "center",
        height: "80%",
        width:"100%"
    });
    var img = $("<img src='/areas/puck/assets/img/ajax-loader.gif'/>");
    el.append(img);
    container.append(el);
    //el.css({
    //    position: "absolute",
    //    top: container.position().top + (container.height() / 2),
    //    left: container.width() / 2
    //});
}
var hideLoader = function () {
    $(".loaderContainer").remove();
}
var displayMarkup = function (parentId, type, variant, fromVariant, contentId, container, msgContainer, shouldGroup, f) {
    var tabId = null;
    var scroll = null;
    container = container || cright;
    shouldGroup = shouldGroup == undefined ? true : shouldGroup;
    f = f || function () { };
    if (container.find(".content_edit_page").length > 0 && contentId && container.find("input[name=Id]").val() == contentId ) {
        tabId = container.find("form>.tab-content>.tab-pane.active").attr("id");
        if (container.parents(".scrollContainer").length>0)
            scroll = container.parents(".scrollContainer:first").find(".simplebar-content-wrapper").scrollTop();
    }
    container.html("");
    //console.log(cinterfaces.find(".type_templates>div").length+" type templates");
    //cinterfaces.find(".type_templates>div").remove();
    showLoader(container);
    var id;
    var variant;
    getMarkup(parentId, type, variant, function (data) {
        container./*hide().*/html(data);
        if (!type) {
            type = container.find("input[name=Type]").val();
            if (!type) return;
        }

        id = container.find("input[name=Id]").val();
        variant = container.find("input[name=Variant]").val();

        workflowComments[id + variant] = undefined;

        cinterfaces.find("div[data-type='" + type + "']").remove();
        //get template for listeditor
        getPrepopulatedMarkup(type, contentId, function (data) {
            //var temp = $("<div/>").append(data);
            //var type = temp.find("input[name='Type']").val();
            cinterfaces.find("div[data-type='" + type + "']").remove();
            cinterfaces.find(".type_templates").append($("<div/>").attr("data-type", type));
            cinterfaces.find("div[data-type='" + type + "']").html(data);
            afterDom();
        });

        var path = container.find("input[name=Path]").val();
        var rootPath = path.indexOf("/", 1) > -1 ? path.substr(0, path.indexOf("/", 1)) : path;
        var translations = $("<ul/>").addClass("translations");
        var node = cleft.find(".node[data-id='" + contentId + "']");
        if (node.length > 0) {
            var dataTranslations = node.attr("data-variants").split(',').sort(function (a, b) {
                var aOrder = getVariantOrder(a, path);
                var bOrder = getVariantOrder(b, path);
                return aOrder - bOrder;
            });
            if (dataTranslations.length > 1) {
                for (var i = 0; i < dataTranslations.length; i++) {
                    (function () {
                        var dataTranslation = dataTranslations[i];
                        var published = true;

                        if (dbcontent[contentId][dataTranslation].Published == false) {
                            published = false;
                        }

                        var dtli = $("<li/>");
                        if (!published)
                            dtli.addClass("unpublished");
                        if (dataTranslation != variant) {
                            var lnk = $("<a href='#content?id=" + contentId + "&variant=" + dataTranslation + "'/>").html("-" + variantNames[dataTranslation]);
                            lnk.click(function (e) {
                                e.preventDefault();
                                var vcode = dataTranslation;
                                displayMarkup(null, type, vcode, undefined, contentId, container, msgContainer, shouldGroup,f);
                            });
                            dtli.append(lnk)
                        } else {
                            dtli.append("-" + variantNames[dataTranslation]);
                        }
                        translations.append(dtli);
                    })();
                }
                container.prepend(translations);
            }
        } else {
            if (contentId != null && contentId != undefined)
                getVariantsForId(contentId, function (d) {
                    d.sort(function (a, b) {
                        var aOrder = getVariantOrder(a.Variant, path);
                        var bOrder = getVariantOrder(b.Variant, path);
                        return aOrder - bOrder;
                    });
                    if (d.length > 1) {
                        for (var i = 0; i < d.length; i++) {
                            (function () {
                                var dtli = $("<li/>");
                                if (!d[i].Published)
                                    dtli.addClass("unpublished");
                                if (d[i].Variant != variant) {
                                    var lnk = $("<a href='#content?id="+contentId+"&variant="+d[i].Variant+"'/>").html("-" + variantNames[d[i].Variant]);
                                    (function () {
                                        var v = d[i].Variant;
                                        lnk.click(function (e) {
                                            e.preventDefault();
                                            displayMarkup(null, type, v, undefined, contentId, container, msgContainer, shouldGroup,f);
                                        });
                                    }());
                                    dtli.append(lnk)
                                } else {
                                    dtli.append("-" + variantNames[d[i].Variant]);
                                }
                                translations.append(dtli);
                            })();
                        }
                        container.prepend(translations);
                    }
                });
        }
        var afterGrouping = function () {
            afterDom();
            container.find('[data-toggle="tooltip"]').tooltip();
            $("body").off("click.ttip").on("click.ttip", function (e) {
                $("div.tooltip[role=tooltip]").remove();
            });
            formDatas[id + variant] = new FormData(container.find("form").get(0));
            container.show();
            container.find(".fieldtabs:first").click();
            container.find(".tab-pane:first").addClass("active");
            setChangeTracker();
            var rightArea = cright.parent().parent(); 
            var rightAreaInOverlay = false;
            if (container.parents(".overlay_screen").length == 1) {
                rightArea = container.parents(".overlay_screen").find(".simplebar-content-wrapper");
                rightAreaInOverlay = true;
            }
            var lastScrollPosition = 0;
            
            $(rightArea).off("scroll.editButtons").on("scroll.editButtons", function (e) {
                var editButtons = container.find(".edit-buttons");
                if (rightAreaInOverlay) {
                    var top = 0;
                    if (rightArea.scrollTop() > 80)
                        top = rightArea.scrollTop() - (container.find("ul.translations").length > 0 ? 79 : 59);
                    else if (rightArea.scrollTop() <= 80) {
                        top = 0;
                    }
                    editButtons.css({ top: top + "px", position: "absolute" });
                } else {
                    var top = 0;
                    if (rightArea.scrollTop() > 80)
                        top = rightArea.scrollTop() - (container.find("ul.translations").length>0?38:0);
                    else if (rightArea.scrollTop() <= 80) {
                        top = 0;
                    }
                    editButtons.css({ top: top + "px", position: "absolute" });

                }
                lastScrollPosition = rightArea.scrollTop();
            });
            if (cleft.find(".node[data-id='" + contentId + "']").length > 0)
                highlightSelectedNodeById(contentId);
            else {
                if(contentId!=undefined)
                    getIdPath(contentId, function (d) {
                        highlightSelectedNodeByIdPath(d);
                    });
            }
            setTimeout(function () {
                $(document).trigger("puckEditFormLoaded", [container]);
            }, 100);
            if (tabId) {
                tabId = tabId.slice(18);
                container.find("[href$='" + tabId + "']").click();
            }
            if (scroll) {
                container.parents(".scrollContainer:first").find(".simplebar-content-wrapper").scrollTop(scroll);
            } else {
                if (contentLocks[id + variant]) {
                    container.find(".content_unlock").focus();
                }
            }
            f();
        }
        //get field groups and build tabs
        var rand = Math.random().toString().substring(0, 6).replace(".", "")
        var tabPrefix = container.attr("data-tabPrefix")+rand;
        var groupedFields = container.find("[data-groupname]");
        if (shouldGroup) {
            if (groupedFields.length > 0) {
                var groups = [];
                $(groupedFields).each(function (i) {
                    var groupName = $(this).attr("data-groupname");
                    if (!groups.contains(groupName))
                        groups.push(groupName);
                });
                var tabHtml = '<ul class="nav nav-tabs" role="tablist">';
                $(groups).each(function (i) {
                    var val = this;
                    tabHtml += '<li class="nav-item ' + (i == 0 ? "active" : "") + '"><a class="nav-link fieldtabs ' + (i == 0 ? "active" : "") + '" data-toggle="tab" role="tab" href="#' + tabPrefix + 'fieldtabs' + i + '">' + val + '</a></li>';
                });
                tabHtml += '<li class="nav-item"><a class="nav-link fieldtabs" data-toggle="tab" role="tab" href="#' + tabPrefix + 'fieldtabs' + groups.length + '">default</a></li>';
                tabHtml += '</ul>';

                tabHtml += '<div class="tab-content">';
                $(groups).each(function (i) {
                    var val = this;
                    tabHtml += '<div data-group="' + val + '" role="tabpanel" class="tab-pane ' + (i == 0 ? "active" : "") + '" id="' + tabPrefix + 'fieldtabs' + i + '"></div>';
                });
                tabHtml += '<div data-group="default" role="tabpanel" class="tab-pane" id="' + tabPrefix + 'fieldtabs' + groups.length + '"></div>';
                tabHtml += "</div>";
                container.find("form").prepend(tabHtml);
                container.find(".nav .fieldtabs").click(function (e) {
                    e.preventDefault();
                    $(this).tab("show");
                });
                $(groupedFields).each(function (i) {
                    var el = $(this);
                    var group = el.attr("data-groupname");
                    var groupContainer = container.find(".tab-pane[data-group='" + group + "']");
                    groupContainer.append(el);
                });
                container.find("div.fields>.fieldwrapper.root").each(function () {
                    var el = $(this);
                    var fieldname = el.attr("data-fieldname");
                    el.appendTo(container.find("[data-group='default']"));
                });
                afterGrouping();
            } else {
                getFieldGroups(type, function (data) {
                    var groups = [];
                    $(data).each(function (i) {
                        var val = this
                        if (!groups.contains(val.split(':')[1]))
                            groups.push(val.split(":")[1]);
                    });
                    var tabHtml = '<ul class="nav nav-tabs" role="tablist">';
                    $(groups).each(function (i) {
                        var val = this;
                        tabHtml += '<li class="nav-item ' + (i == 0 ? "active" : "") + '"><a class="nav-link fieldtabs ' + (i == 0 ? "active" : "") + '" data-toggle="tab" role="tab" href="#' + tabPrefix + 'fieldtabs' + i + '">' + val + '</a></li>';
                    });
                    tabHtml += '<li class="nav-item"><a class="nav-link fieldtabs" data-toggle="tab" role="tab" href="#' + tabPrefix + 'fieldtabs' + groups.length + '">default</a></li>';
                    tabHtml += '</ul>';

                    tabHtml += '<div class="tab-content">';
                    $(groups).each(function (i) {
                        var val = this;
                        tabHtml += '<div data-group="' + val + '" class="tab-pane ' + (i == 0 ? "active" : "") + '" role="tabpanel" id="' + tabPrefix + 'fieldtabs' + i + '"></div>';
                    });
                    tabHtml += '<div data-group="default" class="tab-pane" role="tabpanel" id="' + tabPrefix + 'fieldtabs' + groups.length + '"></div>';
                    tabHtml += "</div>";
                    container.find("form").prepend(tabHtml);
                    container.find(".nav .fieldtabs").click(function (e) {
                        e.preventDefault();
                        $(this).tab("show");
                    });
                    $(data).each(function (i) {
                        var val = this;
                        var type = val.split(":")[0];
                        var group = val.split(":")[1];
                        var field = val.split(":")[2];
                        var fieldWrapper = container.find(".fieldwrapper[data-fieldname='" + field + "']");
                        var groupContainer = container.find(".tab-pane[data-group='" + group + "']");
                        groupContainer.append(fieldWrapper);
                    });
                    container.find("div.fields>.fieldwrapper.root").each(function () {
                        var el = $(this);
                        var fieldname = el.attr("data-fieldname");
                        if (fieldname.split(".").length > 1)
                            container.find(".fieldwrapper[data-fieldname='" + fieldname.split(".").slice(0, -1).join(".") + "']>.editor-field>.fields").append(el);
                        else el.appendTo(container.find("[data-group='default']"));
                    });
                    container.find(".fieldwrapper.complex_child").each(function () {
                        var el = $(this);
                        if (el.find(".fieldwrapper").length == 0 || el.find(".fields").length == 0) {
                            el.addClass("single_field");
                        }
                    });
                    afterGrouping();
                });
            }
        }
        if (!shouldGroup) afterGrouping();
        //publish btn
        if (userRoles.contains("_publish")) {
            container.find(".content_publish").click(function () {
                container.find("input:hidden[name='Published']").val("True");
            });
        } else { container.find(".content_publish").hide(); }
        //udpate btn
        if (userRoles.contains("_edit")) {
            container.find(".content_update").click(function () {
                container.find("input:hidden[name='Published']").val("False");
            });
        } else { container.find(".content_update").hide(); }
        //preview btn
        if (contentId) {
            container.find(".content_preview").click(function (e) {
                e.preventDefault();
                window.open("/puck/preview/PreviewEditor?id=" + contentId + "&variant=" + variant, "_blank");
            });
        } else { container.find(".content_preview").hide(); }
        //unlock btn
        if (contentLocks[id + variant]) {
            
            container.find(".content_unlock").click(function (e) {
                e.preventDefault();
                var el = $(this);
                unlockWorkflowItem(id, variant, function () { });
                el.hide();
                msg(undefined,"content has been unlocked");
            });
            
        }

        wireForm(container.find('form'),
            function (data) {
                var status = true;
                var queued = false;
                if (data.message && data.message.indexOf("queued") > -1) {
                    status = undefined;
                    queued = true;
                } else if (!data.message) {
                    data.message = "content updated.";
                }
                if (contentLocks[id + variant]) {
                    //data.message += ". consider unlocking this content";
                }
                msg(status, data.message, undefined, msgContainer, undefined, function (c) {
                    if (queued) {
                        var checkPublishQueue = function () {
                            setTimeout(function () {
                                getPublishQueue(function (d) {
                                    var inQueue = false;
                                    for (var i = 0; i < d.length; i++) {
                                        if (d[i].Key == id && ((variant && d[i].Value == variant) || !variant))
                                            inQueue = true;
                                    }
                                    if (inQueue)
                                        checkPublishQueue();
                                    else setTimeout(function () { c(); }, 1000);
                                });
                            }, 500);
                        }
                        checkPublishQueue();
                    } else setTimeout(function () {
                        c();
                    }, 5000);
                });
                getDrawContent(data.parentId, undefined, true, function () {
                    if (data.parentId != emptyGuid) {
                        var pnode = cleft.find(".node[data-id='" + data.parentId + "']");
                        pnode.find(".expand:first").removeClass("fa-chevron-right").addClass("fa-chevron-down").css({ visibility: "visible" });
                    }
                    displayMarkup(null, type, variant, undefined, data.id, container, msgContainer, shouldGroup,f);
                },true);

            },
            function (data) {
            container.find(".submitLoader").remove();
            container.find(".content_btns").removeAttr("disabled");
            msg(false, data.message,undefined,msgContainer);
            },
            function (fd) {
                if (window.workflows && isArray(workflows) && workflows[type] && isObject(workflows[type])) {
                    var wfo = workflows[type];

                    var userObject = { username: userName, userRoles: userRoles, userGroups: userGroups };
                    var services = {
                        add: function (status, message, group, assignees, f) {
                            addWorkflowItem(id, variant, status, "none", message, group, type, assignees, function (data) {
                                setTimeout(function () {
                                    workflowItems[id + variant] = data.workflowItem;
                                },500);
                                if (f) f();
                            });
                        },
                        complete: function (status, f) { completeWorkflowItem(id, variant, status || "complete", f || function () { }); },
                        msg: msg
                    };
                    var isPublished = container.find("input[name='Published']").val() == "True";
                    var workflowItem = workflowItems[id + variant];
                    var startingState = formDatas[id + variant];
                    var currentState = fd;
                    
                    if (!workflowComments[id + variant] && wfo.comment && isFunction(wfo.comment)) {
                        var commentDialogTitle = undefined;
                        try {
                            commentDialogTitle = wfo.comment(workflowItem, userObject, startingState, currentState);
                        } catch (ex) {
                            console.error(ex);
                        }
                        if (commentDialogTitle && typeof commentDialogTitle=="string") {

                            //create modal
                            var modalEl = cinterfaces.find(".modal.comment").clone();

                            modalEl.find(".title").html(commentDialogTitle);
                            var select2Input = modalEl.find(".select2:input");
                            getUserNamesJson(function (d) {
                                var values = [];
                                $(d).each(function () {
                                    values.push({id:this,text:this});
                                });
                                select2Input.select2({placeholder:"mention another user...", data: values, multiple: true, width: "100%" });
                            });
                            modalEl.find("button").click(function (e) {
                                var commentVal = modalEl.find("textarea").val();
                                if (commentVal) {
                                    modalEl.modal("hide");
                                    var mentions = [];
                                    if (select2Input.val()!="") {
                                        mentions = select2Input.val().split(",");
                                    }
                                    workflowComments[id + variant] = { comment:commentVal,mentions:mentions };
                                    container.find("form").submit();
                                } else {
                                    modalEl.find("textarea").addClass("input-validation-error");
                                }
                            });

                            modalEl.modal({ keyboard: false, backdrop: "static" });
                            
                            //cancel form submit by returning true
                            return true;
                        }
                    } 
                    var comment = workflowComments[id + variant];
                    if (wfo.handler && isFunction(wfo.handler)) {
                        var cancel = false;
                        try {
                            cancel = wfo.handler(isPublished, workflowItem, userObject, startingState, currentState, comment, services);
                        } catch(ex){
                            console.error(ex);
                        }
                        if (cancel && typeof(cancel) == "boolean")
                            return true;
                    }
                    
                }

                container.find(".content_btns").attr({ disabled: "disabled" });
                var img = $("<img src='/areas/puck/assets/img/tree-loader.gif'/>").addClass("submitLoader");
                if (isMobile()) {
                    img.css({
                        background: "#fff",
                        position: "relative",
                        display: "inlineBlock",
                        top: "1px",
                        right: "1px"
                    });
                    container.find(".edit-buttons").append(img);
                } else {
                    img.css({
                        background: "#fff",
                        position: "relative",
                        display: "inlineBlock",
                        top: "0px",
                        right: "1px",
                        marginTop:"-3px"
                    });
                    container.find(".edit-buttons").prepend(img);
                }

                var id = container.find("[name='Id']").val();
                var variant = container.find("[name='Variant']").val();
                var title = container.find("[name='NodeName']").val();
                if (location.hash.indexOf("content?id=")==-1) {
                    history.replaceState(null, title, "#content?id="+id+"&variant="+variant);
                }

        });

    }, fromVariant, contentId);
}
var isMobile = function () {
    return $("body").hasClass("mobile-ui");
}
var highlightSelectedNodeByIdPath = function (idPath) {
    var ids = idPath.split(",");
    ids.splice(0, 0, emptyGuid);
    var lastId = ids.pop();
    var doGet = function () {
        var id = ids.splice(0, 1)[0];
        var node = cleft.find(".node[data-id='" + id + "']");
        if (node.attr("data-id") != emptyGuid)
            node.find("i.expand:first").removeClass("fa-chevron-right").addClass("fa-chevron-down");
        var afterGet = function () {
            if (ids.length > 0) {
                doGet();
            }
            else
                highlightSelectedNodeById(lastId);
        }
        if (node.find(".node").length == 0) {
            getDrawContent(id, node, true, function () {
                afterGet();
            }, true);
        } else afterGet();    
    }
    doGet();
};
var setChangeTracker = function () {
    changed = false;
    cright.find(":input").change(function (e) {
        changed = true;
    });
}
var msg = function (success, str, shouldRemovePreviousMessages,container,timeout,cancel) {
    timeout = timeout || 5000;
    container = container || cmsg;
    if (shouldRemovePreviousMessages) {
        container.find("div").remove();
    }
    var btnClass = "btn-light";
    if (success === false) { btnClass = "btn-danger"; }
    else if (success === true) { btnClass = "btn-success"; }
    var el = $("<div style='display:none;' class='btn " + btnClass + "'>" + str + "</div>");
    var remove = $("<div class='btn btnclose'>x</div>").click(function () { $(this).parent().remove(); setAreaHeights(); });
    el.append(remove);
    container.html(el);
    el.fadeIn(function () { setAreaHeights(); });
    if (cancel) {
        cancel(function () {
            el.fadeOut(function () { el.remove(); setAreaHeights(); });
        });
    } else {
        setTimeout(function () { el.fadeOut(function () { el.remove(); setAreaHeights(); }); }, timeout);
    }
}
var puckmaxwidth = 960;
var _overlayClose = function () {
    $(".overlayinner,.overlay").remove();
    $("body").css({ overflow: "initial" });
    $(document).unbind("keyup");
}

var overlay = function (el, width, height, top, title, isRightSided) {
    isRightSided = isRightSided || false;
    var overlayClass = isRightSided ? "right" : "left";
    top = top || "0px";
    //overlayClose(false,overlayClass,false);
    var cleftIsVisible = false;
    if (window.innerWidth < 768) {
        if (cleft.is(":visible")) {
            cleftIsVisible = true;
            //console.log("overlay width set from cleft");
            if (width != "100%")
                width = cleft.outerWidth();
            cleft.hide();
        }
    }
    if(width!="100%")
        if (width > window.innerWidth)
            width = window.innerWidth;
    var f = undefined;
    searchDialogClose();
    var outer = $(".interfaces .overlay_screen").clone().addClass("active").addClass(overlayClass).addClass("scrollContainer");
    outer.find(">h1:first").html(title || "")
    var left = (cright.position().left - 30) < -10 ? -14 : (cright.position().left - 30);
    if (isRightSided)
        outer.css({right:"-14px", width: width, top: "0px", height: $(window).height() - 90 + "px" });
    else
        outer.css({ left: left + "px", width: "0px", top: "0px", height: $(window).height() - 90 + "px" });
    if (outer.position().top < $(".rightarea .simplebar-content-wrapper").scrollTop()) {
        outer.css({ top: $(".rightarea .simplebar-content-wrapper").scrollTop()});
    }
    var close = $('<i class="overlay_close fas fa-minus-circle"></i>');
    outer.data("removed",false);
    close.click(function () { overlayClose(cleftIsVisible, overlayClass, undefined, outer); });
    outer.append(close);
    var inner = outer.find(".inner");
    var clear = $("<div class='clearboth'/>");
    width = width || cright.width() - 10;

    inner.append(el).append(clear);
    cright.append(outer);
    
    overlays.unshift(outer);

    var sb3 = new SimpleBar(outer.get(0), {autoHide:false});
    if (!isRightSided)
        outer.animate({ width: width + (width.toString().indexOf("%") > -1 ? "" : "px") }, 200, function () { if (f) f(); afterDom(); });
    else afterDom();

    return outer;
}

var overlayClose = function (showLeftArea, cssClass, highlight,element) {
    if (highlight == undefined) highlight = true;
    if (highlight && cright.find(".content_edit_page").length > 0) {
        var id = cright.find(".content_edit_page input[name=Id]").val();
        if (id)
            highlightSelectedNodeById(id);
    }
    if (element) {
        element.addClass("removed");
        element.remove();
    }
    else
        cright.find(".overlay_screen" + (cssClass ? "." + cssClass : "")).remove();
    $("body").css({ overflow: "initial" });
    $(document).unbind("keyup");
    if ($(window).width() < 768 && showLeftArea) {
        cleft.show();
    }
}

var _overlay = function (el, width, height, top) {
    var ov = $("<div class='overlay'/>");
    var inner = $("<div class='overlayinner container_12'/>");
    var clear = $("<div class='clearboth'/>");
    if (!!width)
        inner.css({ width: width + "px" });
    if (!!height)
        inner.css({ height: height + "px" });
    if (!!top)
        inner.css({ top: top + "px" });
    var close = $("<div class='btn btn-link'><i class='fas fa-minus-circle'/>&nbsp;close</div>");
    close.click(function () {
        overlayClose();
    });
    inner.append(close).append(clear).append(el);
    cright.append(ov).append(inner);
    height = height || $(window).height() * 0.8;
    if (height)
        inner.css({ height: height + "px" });
    inner.css({ left: ($(window).width() - (width || $(".overlayinner").width())) / 2 + "px" });
    if (!top)
        inner.css({ top: ($(window).height() - height) / 2 + "px" });
    $("body").css({ overflow: "hidden" });
    afterDom();
    $(document).keyup(function (e) {
        if (e.keyCode == 27) { overlayClose(); }
    });
}
var afterDomActions = [];
var onAfterDom = function (f) {
    afterDomActions.push(f);
}
var afterDom = function () {
    while (afterDomActions.length) {
        afterDomActions.pop()();
    }
}
var changed = false;
var canChangeMainContent = function () {
    if (cright.find(".fieldwrapper").length > 0 && changed)
        if (confirm("sure you want to move away from this page?")) {
            changed = false;
            return true;
        } else {
            return false;
        }
    else
        return true;
}
var hideTopNav = function () {
    $(".menutop a[data-permission]").each(function () {
        var el = $(this);
        var perm = el.attr("data-permission");
        if (userRoles.contains(perm))
            el.show();
        else
            el.hide();
    });
    if (userRoles.contains("_create")) {
        $(".create_default").show();
    } else {
        $(".create_default").hide();
    }
}

var dirOfPath = function (s) {
    if (s[s.length - 1] == "/")
        return s;
    return s.substring(0, s.lastIndexOf("/") + 1);
}
var isRootItem = function (s) {
    if (s == emptyGuid)
        return true;
    else return false;
    /*var matches = s.match(/\//g);
    if (matches == null)
        return true;
    //throw "isRootItem - invalid input: " + s;
    return matches.length == 1;*/
}
var typeFromChain = function (s) {
    return s.split(" ")[0];
}
var untranslated = function (variants) {
    if (variants == undefined)
        return false;
    var untranslated = [];
    $(languages).each(function () {
        if (!variants.split(",").contains(this.Key))
            untranslated.push(this.Key);
    });
    return untranslated.length == 0 ? false : untranslated;
}

var dialogForVariants = function (variants, multiSelect) {
    multiSelect = multiSelect || false;
    var dialog = $(".interfaces .revision_for_dialog").clone();
    if (multiSelect) {
        dialog.find(".variantcontainer select").attr("multiple", "multiple");
        dialog.find(".variantcontainer label").append("s");
    }
    $.each(variants, function () {
        dialog.find(".variantcontainer select").append("<option value='" + this + "'>" + variantNames[this] + "</option>");
    });
    $.each(languages, function () {
        dialog.find(".descendantscontainer select").append("<option value='" + this.Key + "'>" + variantNames[this.Key] + "</option>");
    });
    dialog.find(".descendantscontainer select").prepend("<option selected value=''>None</option>");
    return dialog;
}
var unpublishedVariants = function (id) {
    var variants = [];
    cleft.find(".node[data-id='" + id + "']>.inner .variant").each(function () {
        if (!$(this).hasClass("published")) {
            if (!variants.includes($(this).attr("data-variant")))
                variants.push($(this).attr("data-variant"));
        }
    });
    return variants.length == 0 ? false : variants;
}
var publishedVariants = function (id) {
    var variants = [];
    cleft.find(".node[data-id='" + id + "']>.inner .variant").each(function () {
        if ($(this).hasClass("published")) {
            if (!variants.includes($(this).attr("data-variant")))
                variants.push($(this).attr("data-variant"));
        }
    });
    return variants.length == 0 ? false : variants;
}
var allVariants = function (id) {
    var variants = [];
    cleft.find(".node[data-id='" + id + "']>.inner .variant").each(function () {
        variants.push($(this).attr("data-variant"));
    });
    return variants.length == 0 ? false : variants;
}
var pollSyncStatus = function (cacheKey) {
    var key = cacheKey || currentCacheKey;
    getCacheItem(key, function (data) {
        if (data.item) {
            if (data.item.indexOf("Error") == 0 || data.item.indexOf("Cancelled") == 0) {
                msg(false,data.item,true,undefined,20000);
            } else if (data.item.indexOf("complete") > -1) {
                msg(true,data.item);
            } else {
                msg(undefined, data.item);
                setTimeout(function () { pollSyncStatus(cacheKey ? cacheKey : undefined); }, 1000);
            }
        }
    });
}

var sync = function (id) {
    getSyncDialog(id, function (data) {
        var overlayEl = overlay(data, 400, 250, undefined, "Sync");
        overlayEl.find("button.cancel").click(function (e) {
            var el = $(this);
            var key = el.attr("data-key");
            cancelSync(key, function (res) {
                el.attr("disabled", "disabled");
                el.html("cancelled");
                el.removeClass("btn-link");
            });
        });
        var form = overlayEl.find('form');
        wireForm(form, function (data) {
            currentCacheKey = data.cacheKey;
            pollSyncStatus();
            overlayClose();
        }, function (data) {
            msg(false, data.message);
        });
    });
}

var setLocalisation = function (p) {
    getLocalisationDialog(p, function (data) {
        var overlayEl = overlay(data, 400, 250, undefined, "Localisation");
        overlayEl.find("option[value='']").remove();
        var form = overlayEl.find('form');
        wireForm(form, function (data) {
            msg(true,"localisation settings saved");
            overlayClose();
            getAllLocalisations(function (data) {
                pathToLocale = data;
            });
        }, function (data) {
            msg(false, data.message);
        });
    });
}
var setDomainMapping = function (p) {
    getDomainMappingDialog(p, function (data) {
        var overlayEl = overlay(data, 500, 250, undefined, "Domain Mapping");
        var form = overlayEl.find('form');
        wireForm(form, function (data) {
            msg(true,"domain mappings saved");
            overlayClose();
        }, function (data) {
            msg(false, data.message);
            //overlayClose();
        });
    });
}
var setNotify = function (p) {
    getNotifyDialog(p, function (data) {
        var overlayEl = overlay(data, 450, 480, undefined, "Notifications");
        var form = overlayEl.find('form');
        wireForm(form, function (data) {
            msg(true,"notifications updated");
            overlayClose();
        }, function (data) {
            msg(false, data.message);
            overlayClose();
        });
    });
}
var highlightSelectedNode = function (path) {
    cleft.find(".node").removeClass("selected");
    cleft.find(".node[data-path='" + path + "']").addClass("selected");
}
var highlightSelectedNodeById = function (id) {
    cleft.find(".node").removeClass("selected");
    cleft.find(".node[data-id='" + id + "']").addClass("selected");
}
var pollRepublishStatus = function () {
    getRepublishEntireSiteStatus(function (data) {
        if (data.Message == "complete") {
            msg(true, data.Message);
        } else if (data.Message.indexOf("error")>-1) {
            msg(false, data.Message);
        }
        else {
            msg(0, data.Message,true)
            setTimeout(pollRepublishStatus,3000);
        }
    });
}
var republishEntireSite = function () {
    if (confirm("Republishing the entire site can take minutes to complete, during which you won't be able to save or publish content, do you want to continue?")) {
        setRepublishEntireSite(function (data) {
            if (data.success) {
                pollRepublishStatus();
            } else {
                pollRepublishStatus();
            };
        });
    }
}
var getVariantOrder = function (variant, path) {
    var variantIsPathLocaleSetting = false;
    var localeSettingForPath;
    for (currentPath in pathToLocale) {
        if ((path + "/").startsWith(currentPath + "/")) {
            localeSettingForPath = pathToLocale[currentPath];
            break;
        }
    }
    if (localeSettingForPath == variant)
        variantIsPathLocaleSetting = true;
    //var order = rootLocalisations[rootPath] == variant ? 0 : (languageSortDictionary[variant] || 100);
    var order = variantIsPathLocaleSetting ? 0 : (languageSortDictionary[variant] || 100);
    return order;
}

var spinningLoaderImg = function (classes) {
    var el = $("<img src='/areas/puck/assets/img/tree-loader.gif'/>");
    if (classes)
        el.addClass(classes);
    return el;
}

var getCropUrl = function (imageVM, cropSizes, cropAlias, anchor) {
    anchor = anchor || "center";
    var url = imageVM.Image.Path;
    if (!cropAlias)
        return url;
    if (cropSizes[cropAlias]) {
        var cropInfo = cropSizes[cropAlias];

        if (url.toLowerCase().indexOf("http")==0) {
            var path = "";
            if (url.toLowerCase().indexOf("https") == 0)
                path = url.replace("https://", "");
            else path = url.replace("http://", "");
            if (path.indexOf("/") == -1)
                url = "/";
            else url = path.substring(path.indexOf("/"));
        }
        var cropModel = null;
        var crops = imageVM.Image.Crops || [];
        for (var i = 0; i < crops.length; i++) {
            if (crops[i].Alias == cropAlias)
                cropModel = crops[i];
        }
        /*check that left,top,right,bottom have values and that the cropmodel width and height match the cropinfo width and height.
         if they don't match, it means that the crop settings have been changed since the crop was saved which should invalidate the crop.*/
        if (cropModel && cropModel.Left && cropModel.Top && cropModel.Right && cropModel.Bottom
            && cropInfo.Width == cropModel.Width && cropInfo.Height == cropModel.Height) {
            url += "?crop="+cropModel.Left+","+cropModel.Top+","+cropModel.Right+","+cropModel.Bottom;
        }
        else {
            url += "?mode=crop&width="+cropInfo.Width+"&height="+cropInfo.Height+"&anchor="+anchor;
        }
    }
    return url;
}
var initTree = function (firstRun) {
    getStartPaths(function (idsAndPaths) {
        startId = emptyGuid;
        var _startIds = [];
        var _startPaths = [];
        for (var i = 0; i < idsAndPaths.length; i++) {
            var entry = idsAndPaths[i];
            _startIds.push(entry.Key);
            _startPaths.push(entry.Value);
        }
        var startPathsChanged = false;
        if (startPaths.length != _startPaths.length)
            startPathsChanged = true;
        if (!startPathsChanged) {
            for (var i = 0; i < _startPaths.length; i++) {
                if (startPaths.indexOf(_startPaths[i]) == -1) {
                    startPathsChanged = true;
                    break;
                }
            }
        }
        if (!startPathsChanged) {
            for (var i = 0; i < startPaths.length; i++) {
                if (_startPaths.indexOf(startPaths[i]) == -1) {
                    startPathsChanged = true;
                    break;
                }
            }
        }

        startPaths = _startPaths.slice(0);
        startIds = _startIds.slice(0);
        if (firstRun || startPathsChanged) {
            if (firstRun) {
                cleft.find("ul.content li:first").attr("data-id", startId);
                cleft.find(".startpath").html("/");
            }
            if (!firstRun && startPathsChanged) {
                cleft.find(".node[data-id=" + emptyGuid + "] > ul").remove();
            }
            getDrawContent(startId, undefined, true, function () { loadTreePaths(_startPaths); }, true);
        }
    });
    setTimeout(initTree, 10000);
}
var loadTreePaths = function (pathsArr, cont, afterDrawContent, renderVariantLinks,sort) {
    var _startPaths = pathsArr.slice(0);
    if (sort == undefined) sort = true;
    if (renderVariantLinks == undefined) renderVariantLinks = true;
    afterDrawContent = afterDrawContent || function () { };
    cont = cont || cleft.find("ul.content");
    if (pathsArr.length == 0) return;
    var firstPath = pathsArr.splice(0, 1)[0];
    loadTreePath(firstPath, function () { loadTreePaths(pathsArr, cont, afterDrawContent, renderVariantLinks,_startPaths); }, cont, afterDrawContent, renderVariantLinks,_startPaths,sort);
}
var loadTreePath = function (path, f, cont, afterDrawContent, renderVariantLinks,_startPaths,sort) {
    var pathSegments = path.split("/");
    var doLoadPath = function (segments, level) {
        if (segments.length >= level) {
            var path = segments.slice(0, level).join("/") || "/";
            var node = cont.find(".node[data-path='" + path + "']:first");
            if (node.length > 0) {
                if (node.attr("data-id") != emptyGuid)
                    node.find(".expand:first").removeClass("fa-chevron-right").addClass("fa-chevron-down");
                if (node.find(".node").length > 0) {
                    doLoadPath(segments, level + 1);
                } else {
                    getDrawContent(node.attr("data-id"), node,sort, function () { if (afterDrawContent) afterDrawContent(); doLoadPath(segments, level + 1); }, renderVariantLinks,_startPaths);
                }
            } else {
                if (f) f();
            }
        } else {
            if (f)
                f();
        }
    }
    doLoadPath(pathSegments, 0);
}

var showWorkflowNotifications = function () {
    getWorkflowNotifications(workflowNotificationId, function (data) {
        if (data.count > 0) {
            latestWorkflowNotificationId = data.id;
            $(".menutop a.content i").html('<div class="badge badge-danger">' + data.count + '</div>');
        }
        setTimeout(showWorkflowNotifications, 10000);
    });
}

//getContentByParentId("", function (res) {
//    var ids = "";
//    for (id in res.current) {
//        ids += id + ",";
//    }
//    if (ids)
//        ids = ids.substr(0, ids.length - 1);
//    getRootsLocalisations(ids, function (res) {
//        for (var i = 0; i < res.length; i++) {
//            rootLocalisations[res[i].path] = res[i].variant;
//        }
//    });
//}, true);


//});   